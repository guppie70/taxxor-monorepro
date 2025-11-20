<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <!-- all, info, debug, error, warning -->
    <xsl:param name="level">all</xsl:param>
    
    <xsl:output method="xml" omit-xml-declaration="no" indent="yes"/>
    
    <xsl:template match="/">
        <log>
            <xsl:choose>
                <xsl:when test="$level = 'all'">
                    <xsl:apply-templates select="/log/entry"/>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:apply-templates select="/log/entry[@level = $level]"/>
                </xsl:otherwise>
            </xsl:choose>
        </log>
    </xsl:template>
    
    <xsl:template match="entry">
        <xsl:choose>
            
            <xsl:when test="ref[property]">
                <xsl:apply-templates select="ref">
                    <xsl:with-param name="set" select="position()"/>
                    <xsl:with-param name="level" select="@level"/>
                    <xsl:with-param name="code" select="@code"/>
                    <xsl:with-param name="edgar-code" select="message/@edgarCode"></xsl:with-param>
                    
                    <xsl:with-param name="message" select="message/text()"/>
                </xsl:apply-templates>
            </xsl:when>
            
            <xsl:otherwise>
                <xsl:call-template name="render-log-entry">
                    <xsl:with-param name="set" select="position()"/>
                    <xsl:with-param name="level" select="@level"/>
                    <xsl:with-param name="code" select="@code"/>
                    <xsl:with-param name="edgar-code" select="message/@edgarCode"/>
                    <xsl:with-param name="ref" select="ref/@href"/>
                    <xsl:with-param name="line" select="ref/@sourceLine"/>
                    <xsl:with-param name="fact" select="message/@fact"/>
                    <xsl:with-param name="value" select="message/@values"/>
                    <xsl:with-param name="valuedisplay"/>
                    <xsl:with-param name="message" select="message/text()"/>    
                </xsl:call-template>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    
    <xsl:template match="ref">
        <xsl:param name="set"/>
        <xsl:param name="level"/>
        <xsl:param name="code"/>
        <xsl:param name="edgar-code"/>
        <xsl:param name="message"/>

        <xsl:call-template name="render-log-entry">
            <xsl:with-param name="set" select="$set"/>
            <xsl:with-param name="level" select="$level"/>
            <xsl:with-param name="code" select="$code"/>
            <xsl:with-param name="edgar-code" select="$edgar-code"/>
            <xsl:with-param name="ref" select="@href"/>
            <xsl:with-param name="line" select="@sourceLine"/>
            <xsl:with-param name="fact" select="property[@name='QName']/@value"/>
            <xsl:with-param name="label" select="property[@name='label']/@value"/>
            <xsl:with-param name="value" select="property[@name='value']/@value"/>
            <xsl:with-param name="valuedisplay" select="property[@name='html value']/@value"/>
            <xsl:with-param name="message" select="$message"/>    
        </xsl:call-template>
    </xsl:template>
    

    
    <xsl:template name="render-log-entry">
        <xsl:param name="set"/>
        <xsl:param name="level"/>
        <xsl:param name="code"/>
        <xsl:param name="edgar-code"/>
        <xsl:param name="ref"/>
        <xsl:param name="line"/>
        <xsl:param name="fact"/>
        <xsl:param name="label"/>
        <xsl:param name="value"/>
        <xsl:param name="valuedisplay"/>
        <xsl:param name="message"/>
        
        <xsl:variable name="message-parsed">
            <xsl:choose>
                <xsl:when test="contains($message, ' - ')">
                    <xsl:value-of select="normalize-space(substring-after(substring-before($message, ' - '), ']'))"/>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="normalize-space(substring-after($message, ']'))"/>
                </xsl:otherwise>
            </xsl:choose>
            
        </xsl:variable>
        
        <entry>
            <set>
                <xsl:value-of select="$set"/>
            </set>
            <level>
                <xsl:value-of select="$level"/>
            </level>
            <code>
                <xsl:value-of select="$code"/>
            </code>
            <edgarcode>
                <xsl:value-of select="$edgar-code"/>
            </edgarcode>
            <ref>
                <xsl:value-of select="$ref"/>
            </ref>
            <line>
                <xsl:value-of select="$line"/>
            </line>
            <fact>
                <xsl:value-of select="$fact"/>
            </fact>
            <label>
                <xsl:value-of select="$label"/>
            </label>
            <value>
                <xsl:value-of select="$value"/>
            </value>
            <valuedisplay>
                <xsl:value-of select="$valuedisplay"/>
            </valuedisplay>
            <message>
                <xsl:value-of select="$message-parsed"/>
            </message>
        </entry>
    </xsl:template>
</xsl:stylesheet>
