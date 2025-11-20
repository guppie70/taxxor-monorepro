<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:param name="language">en</xsl:param>
    <xsl:param name="nodelistzh"/>
    
    <xsl:output method="xml" indent="yes" omit-xml-declaration="yes"/>
    
    
    <xsl:template match="/">
        <localizations-website>
            <translations>
                <xsl:for-each select="/*/*[not(local-name()='mandarin')]">
                    
                    <xsl:variable name="nodename" select="local-name()"/>
                    
                    <xsl:variable name="fragmentidreplace1">
                       <xsl:call-template name="string-replace-all">
                           <xsl:with-param name="text" select="local-name()"/>
                           <xsl:with-param name="replace">_x003</xsl:with-param>
                           <xsl:with-param name="by"></xsl:with-param>
                       </xsl:call-template>
                    </xsl:variable>
                    <xsl:variable name="fragmentid">
                       <xsl:call-template name="string-replace-all">
                           <xsl:with-param name="text" select="$fragmentidreplace1"/>
                           <xsl:with-param name="replace">_x0020_</xsl:with-param>
                           <xsl:with-param name="by"><xsl:text> </xsl:text></xsl:with-param>
                       </xsl:call-template>
                    </xsl:variable>
                    
                    
                    
                    <textfragment id="{$fragmentid}">
                        <xsl:call-template name="create-value">
                            <xsl:with-param name="language" select="$language"/>
                            <xsl:with-param name="node" select="."/>
                        </xsl:call-template>
                        
                        <xsl:if test="//mandarin//*[local-name()=$nodename]">
                            <xsl:call-template name="create-value">
                                <xsl:with-param name="language">zh</xsl:with-param>
                                <xsl:with-param name="node" select="//mandarin//*[local-name()=$nodename]"/>
                            </xsl:call-template>
                            
                        </xsl:if>

                    </textfragment>
                </xsl:for-each>
            </translations>
        </localizations-website>
    </xsl:template>
    
    <xsl:template name="create-value">
        <xsl:param name="language"/>
        <xsl:param name="node"/>
        
        <value lang="{$language}">
            <xsl:choose>
                <xsl:when test="contains($node, '&amp;')">
                    <xsl:value-of select="$node"/>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="$node" disable-output-escaping="yes"/>
                </xsl:otherwise>
            </xsl:choose>
        </value>
        
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
