<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0"> 




    <xsl:template name="get-cell-column-nr">
        <xsl:param name="cell"/>
        <!-- get position of cell, by also accounting for colspans -->
        <xsl:variable name="col-spans" select="sum($cell/preceding-sibling::*[(self::td or self::th) and @colspan]/@colspan)"/>
        <xsl:variable name="no-colspan-cells" select="count($cell/preceding-sibling::*[(self::td or self::th) and not(@colspan)]) + 1"/>
        <xsl:value-of select="$col-spans + $no-colspan-cells"/>
    </xsl:template>
    
    <xsl:template name="get-cell-position">
        <xsl:param name="cell"/>
        <xsl:variable name="is-first-column">
            <xsl:call-template name="is-first-column">
                <xsl:with-param name="cell" select="$cell"/>
            </xsl:call-template>
        </xsl:variable>
        <xsl:variable name="position">
            <xsl:call-template name="get-cell-column-nr">
                <xsl:with-param name="cell" select="$cell"/>
            </xsl:call-template>
            <xsl:if test="$is-first-column = 'true'">
                <xsl:text> first</xsl:text>
            </xsl:if>
            <xsl:if test="not($cell/following-sibling::*[self::td or self::th])">
                <xsl:text> last</xsl:text>
            </xsl:if>
        </xsl:variable>
        <xsl:value-of select="$position"/>
    </xsl:template>
    
    <xsl:template name="is-first-column">
        <xsl:param name="cell"/>
        <xsl:value-of select="$cell and count($cell/preceding-sibling::*[self::td or self::th]) = 0"/>
    </xsl:template>
    
    <xsl:template name="has-no-content">
        <xsl:param name="element"/>
        <xsl:choose>
            <xsl:when test="string-length(normalize-space($element/text()))=0">
                <xsl:value-of select="true()"/>
            </xsl:when>
            <xsl:when test="not($element)">
                <xsl:value-of select="true()"/>
            </xsl:when>
            <xsl:when test="$element/*">
                <xsl:value-of select="false()"/>
            </xsl:when>
            <xsl:when test="$element/ancestor::graph">
                <xsl:value-of select="false()"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="true()"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    
    <xsl:template name="has-content">
        <xsl:param name="element"/>
        <xsl:variable name="has-no-content">
            <xsl:call-template name="has-no-content">
                <xsl:with-param name="element" select="$element"/>
            </xsl:call-template>
        </xsl:variable>
        
        <xsl:choose>
            <xsl:when test="$has-no-content = 'true'">
                <xsl:value-of select="false()"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="true()"/>
            </xsl:otherwise>
        </xsl:choose>
        
    </xsl:template>
    
    
    <xsl:template name="get-paragraph-text">
        <xsl:param name="par"/>
        <xsl:variable name="text">
            <xsl:for-each select="$par/descendant::text()[normalize-space() != '']">
                <xsl:value-of select="."/>
            </xsl:for-each>
        </xsl:variable>
        <xsl:value-of select="$text"/>
    </xsl:template>
    
    
    
    <!-- numbers -->
    <xsl:template name="is-negative-value">
        <xsl:param name="value"/>
        <!-- check if this value is a negative number -->
        <!--
		<xsl:variable name="first-char" select="substring($value,1,1)"/>
		-->
        
        <xsl:variable name="is-number">
            <xsl:call-template name="is-numeric-value">
                <xsl:with-param name="value" select="$value"/>
            </xsl:call-template>
        </xsl:variable>
        <xsl:choose>
            <xsl:when test="$is-number = 'false'">
                <xsl:value-of select="false()"/>
            </xsl:when>
            <xsl:when test="string-length($value) &lt;= 1">
                <xsl:value-of select="false()"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:variable name="has-minus" select="starts-with($value, '-') or starts-with($value, '&#x2212;')"/>
                <xsl:variable name="has-brackets" select="starts-with($value, '(') and contains($value, ')')"/>
                <xsl:choose>
                    <xsl:when test="$has-minus or $has-brackets">
                        <xsl:value-of select="true()"/>
                    </xsl:when>
                    <xsl:otherwise>
                        <xsl:value-of select="false()"/>
                    </xsl:otherwise>
                </xsl:choose>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    
    <xsl:template name="is-numeric-value">
        <xsl:param name="value"/>
        
        <xsl:choose>
            <xsl:when test="normalize-space($value) = ''">
                <xsl:value-of select="false()"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:variable name="clean-value" select="normalize-space(translate($value, '&#x2212;-()%:.,', ''))"/>
                <xsl:choose>
                    <xsl:when test="$clean-value = ''">
                        <xsl:value-of select="false()"/>
                    </xsl:when>
                    <xsl:otherwise>
                        <xsl:variable name="is-number" select="string(number($clean-value)) != 'NaN'"/>
                        <xsl:choose>
                            <xsl:when test="$is-number">
                                <xsl:value-of select="true()"/>
                            </xsl:when>
                            <xsl:otherwise>
                                <xsl:value-of select="false()"/>
                            </xsl:otherwise>
                        </xsl:choose>
                    </xsl:otherwise>
                </xsl:choose>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    
    
    <xsl:template name="is-year-value">
        <xsl:param name="value"/>
        <xsl:variable name="is-numeric-value">
            <xsl:call-template name="is-numeric-value">
                <xsl:with-param name="value" select="$value"/>
            </xsl:call-template>
        </xsl:variable>
        <xsl:value-of select="$is-numeric-value = 'true' and string-length($value) = 4"/>
    </xsl:template>
    
    
    <xsl:template name="is-last-row">
        <xsl:param name="row"/>
        <xsl:value-of select="$row and count($row/following-sibling::tr[not(@hidden = 'true')]) = 0"/>
    </xsl:template>
    
    <xsl:template name="is-first-row">
        <xsl:param name="row"/>
        <xsl:value-of select="$row and count($row/preceding-sibling::tr[not(@hidden = 'true')]) = 0"/>
    </xsl:template>
    
    <xsl:template name="format-number">
        <xsl:param name="value"/>
        
        
        <xsl:choose>
            <xsl:when test="string-length($value) = 0"><xsl:comment>.</xsl:comment></xsl:when>
            <xsl:when test="starts-with($value, '-') or starts-with($value, '&#x2212;')">
                <xsl:text>(</xsl:text>
                <xsl:variable name="absolute-value" select="substring-after(normalize-space(.), '-')"/>
                <xsl:choose>
                    <xsl:when test="contains($absolute-value, '%')">
                        <xsl:value-of select="substring-before($absolute-value, '%')"/>
                        <xsl:text>)%</xsl:text>
                    </xsl:when>
                    <xsl:otherwise>
                        <xsl:value-of select="$absolute-value"/>
                        <xsl:text>)</xsl:text>
                    </xsl:otherwise>
                </xsl:choose>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="normalize-space($value)"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    
    <xsl:template name="string-replace-all">
        <xsl:param name="text"/>
        <xsl:param name="replace"/>
        <xsl:param name="by"/>
        <xsl:choose>
            <xsl:when test="contains($text, $replace)">
                <xsl:value-of select="substring-before($text, $replace)"/>
                <xsl:value-of select="$by"/>
                <xsl:call-template name="string-replace-all">
                    <xsl:with-param name="text" select="substring-after($text, $replace)"/>
                    <xsl:with-param name="replace" select="$replace"/>
                    <xsl:with-param name="by" select="$by"/>
                </xsl:call-template>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="$text"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>  


</xsl:stylesheet>
